using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Xml.Serialization;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace MvcVisionSystem._1._Core
{
    public sealed class YoloTrainingDatasetPreparationService
    {
        public const string AnomalyClassificationRuntimeError =
            "anomaly classification training requires a YOLOv8 or YOLO11 runtime";

        public string LastPreparationFailureMessage { get; private set; } = string.Empty;

        public bool TryPrepare(LabelingProjectData data)
        {
            return TryPrepare(data, out _);
        }

        public TrainingDatasetSnapshot CaptureQueueInput(LabelingProjectData source, RecipeDatasetVersionSnapshot expectedVersion, IEnumerable<TrainingDatasetImageSelection> images, bool onlyReviewed, string inputRoot, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(expectedVersion);
            if (source.ProjectSettings.ExternalYoloDataset.UseForTraining || source.ProjectSettings.ExternalYoloDataset.RequiresExplicitReactivation)
                throw new InvalidOperationException("Queue input requires the recipe's saved images; external dataset setup is required separately.");
            if (!Path.IsPathFullyQualified(inputRoot) || Directory.Exists(inputRoot) || File.Exists(inputRoot))
                throw new ArgumentException("Queue input must use a new absolute directory.", nameof(inputRoot));
            RecipeDatasetVersionSnapshot current = RecipeDatasetVersionService.CreateSnapshot(source);
            if (!string.Equals(current.ContentSha256, expectedVersion.ContentSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The dataset changed before the training input was captured.");
            TrainingDatasetImageSelection[] selected = (images ?? Array.Empty<TrainingDatasetImageSelection>())
                .Where(image => image?.IsEligible(onlyReviewed) == true).ToArray();
            if (selected.Length == 0) throw new InvalidDataException("There are no saved images eligible for training.");
            if (selected.GroupBy(image => Path.GetFileNameWithoutExtension(image.ImagePath), StringComparer.OrdinalIgnoreCase).Any(group => group.Count() != 1))
                throw new InvalidDataException("Training images must have distinct file names.");

            var serializer = new XmlSerializer(typeof(LabelingProjectData));
            using var configuration = new MemoryStream();
            serializer.Serialize(configuration, source); configuration.Position = 0;
            var capturedData = (LabelingProjectData)serializer.Deserialize(configuration);
            capturedData.ConfigureOutputRoot(inputRoot);
            capturedData.ProjectSettings.ImageRootPath = Path.Combine(inputRoot, "source-images");
            capturedData.ProjectSettings.AdditionalImageRootPaths = new List<string>();
            capturedData.ProjectSettings.YoloDataset.ImageSplitAssignments = selected.Select(image => new YoloImageSplitAssignment
                { ImageName = Path.GetFileNameWithoutExtension(image.ImagePath), Split = YoloDatasetSplitService.NormalizeStandardSplit(image.Split) }).ToList();
            Directory.CreateDirectory(inputRoot);
            var capturedImages = new List<string>();
            foreach (TrainingDatasetImageSelection image in selected)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string name = Path.GetFileNameWithoutExtension(image.ImagePath);
                string split = YoloDatasetSplitService.NormalizeStandardSplit(image.Split);
                RecipeDatasetVersionFile[] files = current.Files.Where(file => Path.GetFileNameWithoutExtension(file.RelativePath).Equals(name, StringComparison.OrdinalIgnoreCase)
                    && (file.Split == split || file.Split == "source") && file.Kind is "image" or "label" or "segment" or "mask").ToArray();
                RecipeDatasetVersionFile imageFile = files.Where(file => file.Kind == "image").OrderBy(file => file.Split == "source" ? 1 : 0).FirstOrDefault();
                if (imageFile == null || (source.ProjectSettings.DatasetPurpose != LabelingDatasetPurpose.AnomalyDetection && !files.Any(file => file.Kind is "label" or "segment" or "mask")))
                    throw new InvalidDataException("Saved image or Ground Truth is missing: " + name);
                foreach (RecipeDatasetVersionFile file in files.Where(file => file.Kind != "image"))
                    CopyCapturedFile(Path.Combine(source.OutputRootPath, file.RelativePath), Path.Combine(inputRoot, file.RelativePath), file.Sha256, file.Length, cancellationToken);
                string sourceImage = imageFile.Split == "source"
                    ? source.ProjectSettings.AdditionalImageRootPaths?.Count > 0 ? image.ImagePath : Path.Combine(source.ProjectSettings.ResolveImageRootPath(), imageFile.RelativePath)
                    : Path.Combine(source.OutputRootPath, imageFile.RelativePath);
                string canonicalImage = Path.Combine(inputRoot, "data", split, "images", Path.GetFileName(sourceImage));
                CopyCapturedFile(sourceImage, canonicalImage, imageFile.Sha256, imageFile.Length, cancellationToken);
                string capturedImage = Path.Combine(capturedData.ProjectSettings.ImageRootPath, Path.GetFileName(sourceImage));
                CopyCapturedFile(canonicalImage, capturedImage, imageFile.Sha256, imageFile.Length, cancellationToken);
                capturedImages.Add(capturedImage);
            }

            string reviewHash = string.Empty;
            string qualityReviewPath = YoloImageReviewStatusService.ResolveReviewStatusFilePath(source);
            if (File.Exists(qualityReviewPath))
            {
                using var reviewStream = new FileStream(qualityReviewPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                reviewHash = Convert.ToHexString(SHA256.HashData(reviewStream)); reviewStream.Position = 0;
                using var reader = new StreamReader(reviewStream);
                JArray.Parse(reader.ReadToEnd());
                CopyCapturedFile(qualityReviewPath, YoloImageReviewStatusService.ResolveReviewStatusFilePath(capturedData), reviewHash, reviewStream.Length, cancellationToken);
            }
            var qualityReview = new YoloImageReviewStatusService();
            qualityReview.LoadReviewStatus(capturedData, capturedImages);
            foreach (TrainingDatasetImageSelection image in selected)
            {
                YoloImageReviewStatus savedReview = qualityReview.GetItems().Single(item => item.ImageName.Equals(Path.GetFileNameWithoutExtension(image.ImagePath), StringComparison.OrdinalIgnoreCase));
                if (savedReview.QualityReviewState != image.QualityReviewState)
                    throw new InvalidDataException("The saved quality review changed before training: " + savedReview.ImageName);
            }
            qualityReview.SaveReviewStatus(capturedData, throwOnError: true);
            if (source.ProjectSettings.DatasetPurpose == LabelingDatasetPurpose.AnomalyDetection)
            {
                RecipeDatasetVersionFile reviewFile = current.Files.SingleOrDefault(file => file.Kind == "image-level-label");
                if (reviewFile == null) throw new InvalidDataException("Saved anomaly Ground Truth is missing.");
                CopyCapturedFile(Path.Combine(source.OutputRootPath, reviewFile.RelativePath), Path.Combine(inputRoot, reviewFile.RelativePath), reviewFile.Sha256, reviewFile.Length, cancellationToken);
                var anomalyReview = new AnomalyImageReviewStatusService();
                anomalyReview.LoadReviewStatus(capturedData, capturedImages);
                foreach (TrainingDatasetImageSelection image in selected)
                {
                    AnomalyImageReviewStatus savedReview = anomalyReview.GetItems().Single(item => Path.GetFileNameWithoutExtension(item.ImagePath).Equals(Path.GetFileNameWithoutExtension(image.ImagePath), StringComparison.OrdinalIgnoreCase));
                    if (savedReview.ReviewState != image.AnomalyReviewState || savedReview.ReviewState == AnomalyImageReviewState.Unreviewed)
                        throw new InvalidDataException("The saved anomaly Ground Truth changed before training: " + image.ImagePath);
                }
                anomalyReview.SaveReviewStatus(capturedData, throwOnFailure: true);
            }
            DatasetSplitAssignmentService.SaveContract(capturedData);
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryPrepare(capturedData)) throw new InvalidDataException(LastPreparationFailureMessage);
            RecipeDatasetVersionSnapshot inputVersion = RecipeDatasetVersionService.CreateSnapshot(capturedData);
            if (!string.Equals(RecipeDatasetVersionService.CreateSnapshot(source).ContentSha256, current.ContentSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The dataset changed while training input was being copied.");
            if (reviewHash.Length > 0)
            {
                using var reviewStream = new FileStream(qualityReviewPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (!Convert.ToHexString(SHA256.HashData(reviewStream)).Equals(reviewHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The quality review changed while training input was being copied.");
            }
            else if (File.Exists(qualityReviewPath)) throw new InvalidDataException("A quality review file appeared while training input was being copied.");
            cancellationToken.ThrowIfCancellationRequested();
            AnnotationFilePersistence.WriteAtomically(Path.Combine(inputRoot, "training-input.manifest.json"), temporary => File.WriteAllText(temporary, JsonConvert.SerializeObject(new
            {
                schemaVersion = 1, sourceDatasetVersionId = current.DatasetVersionId, sourceDatasetContentSha256 = current.ContentSha256,
                inputDatasetContentSha256 = inputVersion.ContentSha256, qualityReviewSha256 = reviewHash, onlyReviewed,
                images = selected.Select(image => new { sourceImagePath = image.ImagePath, split = image.Split, qualityReviewState = image.QualityReviewState.ToString(), anomalyReviewState = image.AnomalyReviewState.ToString() })
            }, Formatting.Indented)));
            return new TrainingDatasetSnapshot(capturedData, current.DatasetVersionId, current.ContentSha256, inputVersion.ContentSha256, reviewHash, Array.AsReadOnly(selected));
        }

        private static void CopyCapturedFile(string sourcePath, string targetPath, string expectedHash, long expectedLength, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (sourceStream.Length != expectedLength || !Convert.ToHexString(SHA256.HashData(sourceStream)).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A saved training file changed: " + sourcePath);
            sourceStream.Position = 0;
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
            using var targetStream = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            sourceStream.CopyTo(targetStream); targetStream.Flush(true);
            cancellationToken.ThrowIfCancellationRequested();
        }

        internal bool TryPrepare(LabelingProjectData data, out YoloTrainingDatasetRequest trainingRequest)
        {
            LastPreparationFailureMessage = string.Empty;
            trainingRequest = new YoloTrainingDatasetRequest
            {
                DataPath = data?.DataYamlFilePath ?? string.Empty,
                Task = ResolveTrainingTask(data?.ProjectSettings?.DatasetPurpose ?? LabelingDatasetPurpose.ObjectDetection)
            };

            data?.ProjectSettings?.EnsureDefaults();
            ExternalYoloDatasetSettings externalDataset = data?.ProjectSettings?.ExternalYoloDataset;
            if (externalDataset?.UseForTraining != true && data?.ProjectSettings?.AdditionalImageRootPaths?.Count > 0)
            {
                string missing = data.ProjectSettings.ResolveImageRootPaths().FirstOrDefault(root => !Directory.Exists(root));
                if (missing != null)
                {
                    LastPreparationFailureMessage = string.Format(OpenVisionLab.OpenVisionLanguageService.T("DatasetSourceFolders.Unavailable"), missing);
                    return false;
                }
            }
            string model = data?.ProjectSettings?.PythonModel?.GetProtocolModelName() ?? "yolov5";
            if (string.Equals(model, "unet", StringComparison.OrdinalIgnoreCase))
            {
                return TryPrepareUnetSegmentationTrainingDataset(data, externalDataset, out trainingRequest);
            }

            if (string.Equals(model, "patchcore", StringComparison.OrdinalIgnoreCase))
            {
                return TryPreparePatchCoreAnomalyTrainingDataset(data, out trainingRequest);
            }

            if (externalDataset?.RequiresExplicitReactivation == true)
            {
                LastPreparationFailureMessage = string.IsNullOrWhiteSpace(externalDataset.LastValidationSummary)
                    ? "External YOLO data.yaml requires explicit revalidation and activation before training."
                    : externalDataset.LastValidationSummary;
                AppLog.ABNORMAL($"External YOLO training dataset requires explicit reactivation: {LastPreparationFailureMessage}");
                return false;
            }

            if (externalDataset?.UseForTraining == true)
            {
                return TryPrepareExternalYoloTrainingDataset(data, externalDataset, out trainingRequest);
            }

            if (data?.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.AnomalyDetection)
            {
                return TryPrepareAnomalyClassificationTrainingDataset(data, out trainingRequest);
            }

            YoloSegmentationTrainingLabelExportResult segmentationExportResult = null;
            if (data?.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.Segmentation)
            {
                segmentationExportResult = YoloSegmentationTrainingLabelService.Export(data);
                foreach (string error in segmentationExportResult.Errors)
                {
                    AppLog.ABNORMAL($"YOLO segmentation label export failed: {error}");
                }

                if (!segmentationExportResult.IsReady)
                {
                    LastPreparationFailureMessage = string.Join(Environment.NewLine, segmentationExportResult.Errors);
                    return false;
                }
            }

            YoloDatasetReadinessReport report = YoloDatasetReadinessService.Build(data, refreshYaml: true);
            foreach (string error in report.Errors)
            {
                AppLog.ABNORMAL($"YOLO 학습 준비 점검 실패: {error}");
            }

            if (!report.IsReady)
            {
                LastPreparationFailureMessage = string.Join(Environment.NewLine, report.Errors);
                return false;
            }

            foreach (string line in report.SummaryLines)
            {
                AppLog.NORMAL(line);
            }

            if (data?.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.Segmentation)
            {
                AppLog.NORMAL($"YOLO segmentation labels ready. Images:{segmentationExportResult.ImageCount}, LabelFiles:{segmentationExportResult.LabelFileCount}, Polygons:{segmentationExportResult.PolygonCount}, Backgrounds:{segmentationExportResult.BackgroundImageCount}");
            }

            return true;
        }

        private bool TryPrepareExternalYoloTrainingDataset(
            LabelingProjectData data,
            ExternalYoloDatasetSettings externalDataset,
            out YoloTrainingDatasetRequest trainingRequest)
        {
            YoloExternalDatasetIntakeReport report = YoloExternalDatasetIntakeService.Build(
                externalDataset?.DataYamlFilePath,
                externalDataset?.DatasetPurpose ?? LabelingDatasetPurpose.ObjectDetection);
            if (!report.IsReady)
            {
                YoloExternalDatasetIntakeService.ApplyValidation(externalDataset, report);
                LastPreparationFailureMessage = string.Join(Environment.NewLine, report.Errors);
                foreach (string error in report.Errors)
                {
                    AppLog.ABNORMAL($"External YOLO training dataset validation failed: {error}");
                }

                trainingRequest = new YoloTrainingDatasetRequest();
                return false;
            }

            if (!YoloExternalDatasetIntakeService.HasCurrentSourceIdentity(externalDataset, report, out string identityError))
            {
                YoloExternalDatasetIntakeService.ApplyValidation(externalDataset, report);
                YoloExternalDatasetIntakeService.MarkSourceIdentityRequiresReactivation(externalDataset, identityError);
                LastPreparationFailureMessage = identityError;
                AppLog.ABNORMAL($"External YOLO training dataset source identity changed: {identityError}");
                trainingRequest = new YoloTrainingDatasetRequest();
                return false;
            }

            YoloExternalDatasetIntakeService.ApplyValidation(externalDataset, report);

            string runtimeParentPath = Path.Combine(data?.OutputRootPath ?? string.Empty, "external-yolo-runtime");
            YoloExternalRuntimeDatasetResult runtime = YoloExternalDatasetIntakeService.PrepareRuntimeDataset(
                report.DataYamlFilePath,
                report.Purpose,
                runtimeParentPath);
            if (!runtime.IsReady)
            {
                LastPreparationFailureMessage = string.Join(Environment.NewLine, runtime.Errors);
                foreach (string error in runtime.Errors)
                {
                    AppLog.ABNORMAL($"External YOLO runtime dataset preparation failed: {error}");
                }

                trainingRequest = new YoloTrainingDatasetRequest();
                return false;
            }

            trainingRequest = new YoloTrainingDatasetRequest
            {
                DataPath = runtime.RuntimeDataYamlFilePath,
                Task = ResolveTrainingTask(report.Purpose),
                IsExternalSource = true,
                SourceFingerprintSha256 = report.SourceFingerprintSha256,
                RuntimeDataYamlFilePath = runtime.RuntimeDataYamlFilePath,
                DatasetVersionId = RecipeDatasetVersionService.BuildExternalDatasetVersionId(report.SourceFingerprintSha256),
                DatasetContentSha256 = report.SourceFingerprintSha256
            };
            AppLog.NORMAL($"External native YOLO dataset ready. {report.Summary} / Source:{report.DataYamlFilePath} / Runtime:{runtime.RuntimeDataYamlFilePath}");
            return true;
        }

        private bool TryPrepareAnomalyClassificationTrainingDataset(
            LabelingProjectData data,
            out YoloTrainingDatasetRequest trainingRequest)
        {
            trainingRequest = new YoloTrainingDatasetRequest
            {
                DataPath = string.Empty,
                Task = "classify"
            };

            string model = data?.ProjectSettings?.PythonModel?.GetProtocolModelName() ?? "yolov5";
            if (!string.Equals(model, "yolov8", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(model, "yolo11", StringComparison.OrdinalIgnoreCase))
            {
                LastPreparationFailureMessage = $"{AnomalyClassificationRuntimeError}. Current:{model}";
                AppLog.ABNORMAL($"YOLO anomaly classification training blocked: {LastPreparationFailureMessage}");
                return false;
            }

            AnomalyClassificationTrainingReadinessReport readiness =
                AnomalyClassificationTrainingReadinessService.Build(data);
            if (!readiness.IsReady)
            {
                LastPreparationFailureMessage = string.Join(Environment.NewLine, readiness.Errors);
                foreach (string error in readiness.Errors)
                {
                    AppLog.ABNORMAL($"YOLO anomaly classification training failed: {error}");
                }

                return false;
            }

            AnomalyClassificationDatasetExportResult result;
            try
            {
                var exportService = new AnomalyClassificationDatasetExportService();
                result = exportService.Export(data, readiness.SourceImagePaths);
            }
            catch (Exception ex)
            {
                LastPreparationFailureMessage = $"classification dataset export failed. {ex.Message}";
                AppLog.ABNORMAL($"YOLO anomaly classification training failed: {LastPreparationFailureMessage}");
                return false;
            }
            if (result.NormalImageCount == 0 || result.AbnormalImageCount == 0)
            {
                LastPreparationFailureMessage = $"{AnomalyClassificationTrainingReadinessService.NeedsReviewedNormalAndAbnormalError}. Normal:{result.NormalImageCount}, Abnormal:{result.AbnormalImageCount}";
                AppLog.ABNORMAL($"YOLO anomaly classification training failed: {LastPreparationFailureMessage}");
                return false;
            }

            AppLog.NORMAL($"YOLO anomaly classification dataset ready. Normal:{result.NormalImageCount}, Abnormal:{result.AbnormalImageCount}, Skipped:{result.SkippedImageCount}, Path:{result.DatasetRootPath}");
            trainingRequest.DataPath = result.DatasetRootPath;
            return true;
        }

        private bool TryPreparePatchCoreAnomalyTrainingDataset(
            LabelingProjectData data,
            out YoloTrainingDatasetRequest trainingRequest)
        {
            trainingRequest = new YoloTrainingDatasetRequest
            {
                DataPath = string.Empty,
                Task = "anomaly"
            };
            PatchCoreAnomalyTrainingReadinessReport readiness =
                PatchCoreAnomalyTrainingReadinessService.Build(data);
            if (!readiness.IsReady)
            {
                LastPreparationFailureMessage = string.Join(Environment.NewLine, readiness.Errors);
                foreach (string error in readiness.Errors)
                {
                    AppLog.ABNORMAL($"PatchCore anomaly training failed: {error}");
                }

                return false;
            }

            AnomalyClassificationDatasetExportResult result;
            try
            {
                result = new AnomalyClassificationDatasetExportService()
                    .Export(data, readiness.SourceImagePaths, Path.Combine(data.OutputRootPath, "patchcore"));
            }
            catch (Exception ex)
            {
                LastPreparationFailureMessage = $"PatchCore dataset export failed. {ex.Message}";
                AppLog.ABNORMAL(LastPreparationFailureMessage);
                return false;
            }

            string trainNormalPath = Path.Combine(
                result.DatasetRootPath,
                YoloDatasetSplitService.TrainMode,
                AnomalyClassificationDatasetExportService.NormalClassFolderName);
            int exportedTrainNormalCount = Directory.Exists(trainNormalPath)
                ? Directory.EnumerateFiles(trainNormalPath, "*", SearchOption.AllDirectories).Count()
                : 0;
            if (exportedTrainNormalCount < 2)
            {
                LastPreparationFailureMessage = $"{PatchCoreAnomalyTrainingReadinessService.NeedsReviewedNormalError}. ExportedTrainNormal:{exportedTrainNormalCount}";
                AppLog.ABNORMAL(LastPreparationFailureMessage);
                return false;
            }

            foreach (string warning in readiness.Warnings)
            {
                AppLog.NORMAL($"PatchCore anomaly training warning: {warning}");
            }

            trainingRequest.DataPath = result.DatasetRootPath;
            AppLog.NORMAL($"PatchCore normal-only dataset ready. TrainNormal:{readiness.TrainNormalCount}, ValidationNormal:{readiness.ValidationNormalCount}, ReviewedAbnormalExcludedFromLearning:{readiness.ReviewedAbnormalCount}, Path:{result.DatasetRootPath}");
            return true;
        }

        private bool TryPrepareUnetSegmentationTrainingDataset(
            LabelingProjectData data,
            ExternalYoloDatasetSettings externalDataset,
            out YoloTrainingDatasetRequest trainingRequest)
        {
            trainingRequest = new YoloTrainingDatasetRequest
            {
                DataPath = string.Empty,
                Task = "segment"
            };
            if (data?.ProjectSettings?.DatasetPurpose != LabelingDatasetPurpose.Segmentation)
            {
                LastPreparationFailureMessage = "U-Net training requires a segmentation recipe with masks or polygons.";
                AppLog.ABNORMAL(LastPreparationFailureMessage);
                return false;
            }

            if (externalDataset?.RequiresExplicitReactivation == true)
            {
                LastPreparationFailureMessage = string.IsNullOrWhiteSpace(externalDataset.LastValidationSummary)
                    ? "External YOLO data.yaml requires explicit revalidation and activation before U-Net training."
                    : externalDataset.LastValidationSummary;
                AppLog.ABNORMAL(LastPreparationFailureMessage);
                return false;
            }

            if (externalDataset?.UseForTraining == true)
            {
                return TryPrepareExternalUnetSegmentationTrainingDataset(data, externalDataset, out trainingRequest);
            }

            UnetSegmentationDatasetExportResult result = UnetSegmentationDatasetExportService.Export(data);
            foreach (string error in result.Errors)
            {
                AppLog.ABNORMAL($"U-Net segmentation export failed: {error}");
            }

            if (!result.IsReady)
            {
                LastPreparationFailureMessage = string.Join(Environment.NewLine, result.Errors);
                return false;
            }

            trainingRequest.DataPath = result.OutputRootPath;
            AppLog.NORMAL($"U-Net segmentation dataset ready. Images:{result.ImageCount}, PositiveMasks:{result.PositiveMaskImageCount}, Path:{result.OutputRootPath}");
            return true;
        }

        private bool TryPrepareExternalUnetSegmentationTrainingDataset(
            LabelingProjectData data,
            ExternalYoloDatasetSettings externalDataset,
            out YoloTrainingDatasetRequest trainingRequest)
        {
            trainingRequest = new YoloTrainingDatasetRequest
            {
                DataPath = string.Empty,
                Task = "segment"
            };
            if (externalDataset?.DatasetPurpose != LabelingDatasetPurpose.Segmentation)
            {
                LastPreparationFailureMessage = "U-Net external data.yaml training requires a Segmentation native YOLO source.";
                AppLog.ABNORMAL(LastPreparationFailureMessage);
                return false;
            }

            YoloExternalDatasetIntakeReport report = YoloExternalDatasetIntakeService.Build(
                externalDataset.DataYamlFilePath,
                LabelingDatasetPurpose.Segmentation);
            if (!report.IsReady)
            {
                YoloExternalDatasetIntakeService.ApplyValidation(externalDataset, report);
                LastPreparationFailureMessage = string.Join(Environment.NewLine, report.Errors);
                foreach (string error in report.Errors)
                {
                    AppLog.ABNORMAL($"External U-Net segmentation dataset validation failed: {error}");
                }
                return false;
            }

            if (!YoloExternalDatasetIntakeService.HasCurrentSourceIdentity(externalDataset, report, out string identityError))
            {
                YoloExternalDatasetIntakeService.ApplyValidation(externalDataset, report);
                YoloExternalDatasetIntakeService.MarkSourceIdentityRequiresReactivation(externalDataset, identityError);
                LastPreparationFailureMessage = identityError;
                AppLog.ABNORMAL($"External U-Net segmentation source identity changed: {identityError}");
                return false;
            }

            YoloExternalDatasetIntakeService.ApplyValidation(externalDataset, report);
            UnetSegmentationDatasetExportResult result =
                ExternalYoloSegmentationCanonicalExportService.Export(data, report.DataYamlFilePath);
            foreach (string error in result.Errors)
            {
                AppLog.ABNORMAL($"External U-Net segmentation canonical export failed: {error}");
            }
            if (!result.IsReady)
            {
                LastPreparationFailureMessage = string.Join(Environment.NewLine, result.Errors);
                return false;
            }

            trainingRequest = new YoloTrainingDatasetRequest
            {
                DataPath = result.OutputRootPath,
                Task = "segment",
                IsExternalSource = true,
                SourceFingerprintSha256 = report.SourceFingerprintSha256,
                RuntimeDataYamlFilePath = result.OutputRootPath,
                DatasetVersionId = RecipeDatasetVersionService.BuildExternalDatasetVersionId(report.SourceFingerprintSha256),
                DatasetContentSha256 = report.SourceFingerprintSha256
            };
            AppLog.NORMAL($"External native YOLO segmentation U-Net dataset ready. Images:{result.ImageCount}, PositiveMasks:{result.PositiveMaskImageCount}, Source:{report.DataYamlFilePath}, Canonical:{result.OutputRootPath}");
            return true;
        }

        private static string ResolveTrainingTask(LabelingDatasetPurpose datasetPurpose)
        {
            return datasetPurpose == LabelingDatasetPurpose.Segmentation
                ? "segment"
                : "detect";
        }
    }

    public sealed record TrainingDatasetImageSelection(string ImagePath, string Split, bool HasSavedGroundTruth, bool IsSaveRequired, YoloImageQualityReviewState QualityReviewState, AnomalyImageReviewState AnomalyReviewState = AnomalyImageReviewState.Unreviewed)
    {
        public bool IsEligible(bool onlyReviewed) => HasSavedGroundTruth && !IsSaveRequired && QualityReviewState != YoloImageQualityReviewState.NeedsFix
            && YoloDatasetSplitService.NormalizeStandardSplit(Split).Length > 0 && (!onlyReviewed || QualityReviewState == YoloImageQualityReviewState.Reviewed);
    }

    public sealed record TrainingDatasetSnapshot(LabelingProjectData Data, string SourceDatasetVersionId, string SourceDatasetContentSha256, string InputDatasetContentSha256, string QualityReviewSha256, IReadOnlyList<TrainingDatasetImageSelection> Images);

    internal sealed class YoloTrainingDatasetRequest
    {
        public string DataPath { get; set; } = string.Empty;

        public string Task { get; set; } = "detect";

        public bool IsExternalSource { get; set; }

        public string SourceFingerprintSha256 { get; set; } = string.Empty;

        public string RuntimeDataYamlFilePath { get; set; } = string.Empty;

        public string DatasetVersionId { get; set; } = string.Empty;

        public string DatasetContentSha256 { get; set; } = string.Empty;
    }
}
