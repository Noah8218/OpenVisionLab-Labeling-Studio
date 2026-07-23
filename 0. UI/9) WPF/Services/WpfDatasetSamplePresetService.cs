using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    public sealed class WpfDatasetSamplePresetApplyResult
    {
        public bool Applied { get; set; }

        public string ImageRootPath { get; set; } = string.Empty;

        public string SummaryText { get; set; } = string.Empty;
    }

    public static class WpfDatasetSamplePresetService
    {
        private static readonly string[] ImageExtensions =
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".bmp",
            ".webp",
            ".tif",
            ".tiff"
        };

        public static WpfDatasetSamplePresetItem CreateEmptyPreset(LabelingDatasetPurpose purpose)
            => new WpfDatasetSamplePresetItem(
                WpfDatasetSamplePresetKind.Empty,
                purpose,
                "빈 프로젝트",
                "샘플 없이 새 데이터셋 구조만 만듭니다.",
                string.Empty,
                string.Empty,
                GetDefaultClassNames(purpose),
                isAvailable: true,
                "새 라벨을 직접 만들 준비");

        public static IReadOnlyList<WpfDatasetSamplePresetItem> BuildPresets(LabelingDatasetPurpose purpose)
        {
            List<WpfDatasetSamplePresetItem> presets = new List<WpfDatasetSamplePresetItem>
            {
                CreateEmptyPreset(purpose)
            };

            if (purpose == LabelingDatasetPurpose.ObjectDetection)
            {
                ResolveCoco128Source(out string imageRoot, out string labelRoot);
                bool available = Directory.Exists(imageRoot) && Directory.Exists(labelRoot);
                presets.Add(new WpfDatasetSamplePresetItem(
                    WpfDatasetSamplePresetKind.Coco128ObjectDetection,
                    purpose,
                    "COCO128 객체탐지 샘플",
                    "사람, 차량, 물체가 포함된 YOLO box 샘플입니다.",
                    imageRoot,
                    labelRoot,
                    GetCocoClassNames(),
                    available,
                    available ? "이미지/YOLO 라벨 128개 준비됨" : "datasets/object-detection/coco128 다운로드 필요"));
                string industrialImageRoot = ResolveIndustrialObjectImageRoot();
                bool industrialAvailable = Directory.Exists(industrialImageRoot);
                presets.Add(new WpfDatasetSamplePresetItem(
                    WpfDatasetSamplePresetKind.IndustrialObjectDetectionImages,
                    purpose,
                    "Kolektor \uC0B0\uC5C5 \uC774\uBBF8\uC9C0(\uBC15\uC2A4)",
                    "\uC2E4\uC81C \uC0B0\uC5C5 \uC774\uBBF8\uC9C0\uC5D0 \uC9C1\uC811 \uBC15\uC2A4 \uB77C\uBCA8\uC744 \uADF8\uB9AC\uB294 \uAC1D\uCCB4\uD0D0\uC9C0 \uC0D8\uD50C\uC785\uB2C8\uB2E4.",
                    industrialImageRoot,
                    string.Empty,
                    new[] { "Defect" },
                    industrialAvailable,
                    industrialAvailable ? "kos \uC0B0\uC5C5 \uC774\uBBF8\uC9C0 \uC900\uBE44\uB428" : "KolektorSDD app/data/train/images \uACBD\uB85C \uD544\uC694"));
            }

            if (purpose == LabelingDatasetPurpose.Segmentation || purpose == LabelingDatasetPurpose.AnomalyDetection)
            {
                string industrialRoot = ResolveIndustrialDefectRoot();
                bool available = Directory.Exists(industrialRoot);
                presets.Add(new WpfDatasetSamplePresetItem(
                    WpfDatasetSamplePresetKind.IndustrialDefectMasks,
                    purpose,
                    "산업 결함 마스크 샘플",
                    "원본 이미지와 *_label.bmp 마스크를 함께 복사합니다.",
                    industrialRoot,
                    industrialRoot,
                    new[] { "OK", "NG" },
                    available,
                    available ? "원본/마스크 폴더 감지됨" : "KolektorSDD 같은 산업 샘플 경로 필요"));
            }

            return presets;
        }

        public static bool TryApplySample(
            WpfDatasetSetupRequest request,
            CData data,
            out WpfDatasetSamplePresetApplyResult result,
            out string error)
        {
            result = new WpfDatasetSamplePresetApplyResult();
            error = string.Empty;

            if (request == null || data == null || request.SamplePresetKind == WpfDatasetSamplePresetKind.Empty)
            {
                return true;
            }

            data.NormalizeOutputPaths();
            data.EnsureYoloOutputDirectories();
            data.ProjectSettings.EnsureDefaults();

            switch (request.SamplePresetKind)
            {
                case WpfDatasetSamplePresetKind.Coco128ObjectDetection:
                    return TryApplyCoco128(data, out result, out error);

                case WpfDatasetSamplePresetKind.IndustrialObjectDetectionImages:
                    return TryApplyIndustrialObjectDetectionImages(data, out result, out error);

                case WpfDatasetSamplePresetKind.IndustrialDefectMasks:
                    return TryApplyIndustrialDefectMasks(data, out result, out error);

                default:
                    error = $"지원하지 않는 샘플 프리셋입니다: {request.SamplePresetKind}";
                    return false;
            }
        }

        public static IReadOnlyList<string> GetDefaultClassNames(LabelingDatasetPurpose purpose)
        {
            return purpose switch
            {
                LabelingDatasetPurpose.ObjectDetection => new[] { "Defect" },
                LabelingDatasetPurpose.Segmentation => new[] { "Defect" },
                LabelingDatasetPurpose.AnomalyDetection => new[] { "OK", "NG" },
                _ => new[] { "Defect" }
            };
        }

        private static bool TryApplyCoco128(CData data, out WpfDatasetSamplePresetApplyResult result, out string error)
        {
            result = new WpfDatasetSamplePresetApplyResult();
            error = string.Empty;
            ResolveCoco128Source(out string imageRoot, out string labelRoot);
            if (!Directory.Exists(imageRoot) || !Directory.Exists(labelRoot))
            {
                error = "COCO128 샘플 이미지/라벨 폴더를 찾지 못했습니다.";
                return false;
            }

            string targetImages = data.ProjectSettings.YoloDataset.TrainImagesPath;
            string targetLabels = data.ProjectSettings.YoloDataset.TrainLabelsPath;
            int imageCount = CopyFiles(imageRoot, targetImages, IsImageFile);
            int labelCount = CopyFiles(labelRoot, targetLabels, path => string.Equals(Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase));

            result = new WpfDatasetSamplePresetApplyResult
            {
                Applied = true,
                ImageRootPath = targetImages,
                SummaryText = $"COCO128 샘플 적용: images {imageCount}, labels {labelCount}"
            };
            return true;
        }

        private static bool TryApplyIndustrialObjectDetectionImages(CData data, out WpfDatasetSamplePresetApplyResult result, out string error)
        {
            result = new WpfDatasetSamplePresetApplyResult();
            error = string.Empty;
            string imageRoot = ResolveIndustrialObjectImageRoot();
            if (!Directory.Exists(imageRoot))
            {
                error = "Kolektor \uC0B0\uC5C5 \uC774\uBBF8\uC9C0 \uD3F4\uB354\uB97C \uCC3E\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
                return false;
            }

            string targetImages = data.ProjectSettings.YoloDataset.TrainImagesPath;
            int imageCount = CopyUniqueImageStemFiles(
                imageRoot,
                targetImages,
                path => IsImageFile(path) && !IsMaskLabelFile(path),
                SearchOption.AllDirectories);

            result = new WpfDatasetSamplePresetApplyResult
            {
                Applied = true,
                ImageRootPath = targetImages,
                SummaryText = $"Kolektor \uC0B0\uC5C5 \uC774\uBBF8\uC9C0 \uC0D8\uD50C \uC801\uC6A9: images {imageCount}, labels 0"
            };
            return true;
        }

        private static bool TryApplyIndustrialDefectMasks(CData data, out WpfDatasetSamplePresetApplyResult result, out string error)
        {
            result = new WpfDatasetSamplePresetApplyResult();
            error = string.Empty;
            string sourceRoot = ResolveIndustrialDefectRoot();
            if (!Directory.Exists(sourceRoot))
            {
                error = "산업 결함 샘플 폴더를 찾지 못했습니다.";
                return false;
            }

            string targetImages = data.ProjectSettings.YoloDataset.TrainImagesPath;
            string targetMasks = Path.Combine(data.ProjectSettings.YoloDataset.OutputRootPath, "data", "train", "masks");
            int imageCount = CopyFiles(sourceRoot, targetImages, path => IsImageFile(path) && !IsMaskLabelFile(path), SearchOption.AllDirectories);
            int maskCount = CopyFiles(sourceRoot, targetMasks, IsMaskLabelFile, SearchOption.AllDirectories);

            result = new WpfDatasetSamplePresetApplyResult
            {
                Applied = true,
                ImageRootPath = targetImages,
                SummaryText = $"산업 결함 샘플 적용: images {imageCount}, masks {maskCount}"
            };
            return true;
        }

        private static int CopyFiles(string sourceRoot, string targetRoot, Func<string, bool> predicate, SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            Directory.CreateDirectory(targetRoot);
            int count = 0;
            foreach (string sourceFile in Directory.EnumerateFiles(sourceRoot, "*.*", searchOption).Where(predicate))
            {
                string targetFile = Path.Combine(targetRoot, Path.GetFileName(sourceFile));
                File.Copy(sourceFile, targetFile, overwrite: true);
                count++;
            }

            return count;
        }

        private static int CopyUniqueImageStemFiles(string sourceRoot, string targetRoot, Func<string, bool> predicate, SearchOption searchOption)
        {
            Directory.CreateDirectory(targetRoot);
            var copiedStems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int count = 0;
            foreach (string sourceFile in Directory.EnumerateFiles(sourceRoot, "*.*", searchOption)
                         .Where(predicate)
                         .OrderBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase)
                         .ThenBy(path => GetImageExtensionPriority(Path.GetExtension(path)))
                         .ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            {
                string stem = Path.GetFileNameWithoutExtension(sourceFile);
                if (!copiedStems.Add(stem))
                {
                    continue;
                }

                // Industrial sample dumps can contain both .jpg and .jpeg for the
                // same part. The labeling queue and YOLO txt labels are stem-based,
                // so copying both creates ambiguous review/save behavior.
                string targetFile = Path.Combine(targetRoot, Path.GetFileName(sourceFile));
                File.Copy(sourceFile, targetFile, overwrite: true);
                count++;
            }

            return count;
        }

        private static int GetImageExtensionPriority(string extension)
        {
            if (string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".bmp", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            return 2;
        }

        private static bool IsImageFile(string path)
            => ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

        private static bool IsMaskLabelFile(string path)
            => Path.GetFileNameWithoutExtension(path).EndsWith("_label", StringComparison.OrdinalIgnoreCase)
                && IsImageFile(path);

        private static void ResolveCoco128Source(out string imageRoot, out string labelRoot)
        {
            string repositoryRoot = FindRepositoryRoot();
            foreach ((string images, string labels) candidate in new[]
            {
                (
                    Path.Combine(repositoryRoot, "datasets", "object-detection", "app-layout", "KolektorObjectSample", "train", "images"),
                    Path.Combine(repositoryRoot, "datasets", "object-detection", "app-layout", "KolektorObjectSample", "train", "labels")
                ),
                (
                    Path.Combine(repositoryRoot, "datasets", "object-detection", "coco128", "coco128", "images", "train2017"),
                    Path.Combine(repositoryRoot, "datasets", "object-detection", "coco128", "coco128", "labels", "train2017")
                )
            })
            {
                if (Directory.Exists(candidate.images) && Directory.Exists(candidate.labels))
                {
                    imageRoot = candidate.images;
                    labelRoot = candidate.labels;
                    return;
                }
            }

            imageRoot = string.Empty;
            labelRoot = string.Empty;
        }

        private static string ResolveIndustrialDefectRoot()
        {
            string repositoryRoot = FindRepositoryRoot();
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            foreach (string candidate in new[]
            {
                Path.Combine(repositoryRoot, "datasets", "industrial", "KolektorSDD"),
                Path.Combine(userProfile, "LabelingIndustrialDatasets", "KolektorSDD"),
                @"C:\temp\kolektor_test\KolektorSDD"
            })
            {
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            return Path.Combine(repositoryRoot, "datasets", "industrial", "KolektorSDD");
        }

        private static string ResolveIndustrialObjectImageRoot()
        {
            string repositoryRoot = FindRepositoryRoot();
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            foreach (string candidate in new[]
            {
                Path.Combine(repositoryRoot, "datasets", "industrial", "KolektorSDD", "app", "data", "train", "images"),
                Path.Combine(userProfile, "LabelingIndustrialDatasets", "KolektorSDD", "app", "data", "train", "images"),
                @"C:\temp\kolektor_test\KolektorSDD\app\data\train\images"
            })
            {
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            return Path.Combine(repositoryRoot, "datasets", "industrial", "KolektorSDD", "app", "data", "train", "images");
        }

        private static string FindRepositoryRoot()
        {
            foreach (string startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                string current = startPath;
                while (!string.IsNullOrWhiteSpace(current))
                {
                    if (File.Exists(Path.Combine(current, "OpenVisionLab.LabelingStudio.sln"))
                        || File.Exists(Path.Combine(current, "OpenVisionLab.LabelingStudio.csproj"))
                        || File.Exists(Path.Combine(current, "MvcVisionSystem.sln"))
                        || File.Exists(Path.Combine(current, "MvcVisionSystem.csproj")))
                    {
                        return current;
                    }

                    current = Directory.GetParent(current)?.FullName;
                }
            }

            return Directory.GetCurrentDirectory();
        }

        private static IReadOnlyList<string> GetCocoClassNames()
            => new[]
            {
                "person",
                "bicycle",
                "car",
                "motorcycle",
                "airplane",
                "bus",
                "train",
                "truck",
                "boat",
                "traffic light",
                "fire hydrant",
                "stop sign",
                "parking meter",
                "bench",
                "bird",
                "cat",
                "dog",
                "horse",
                "sheep",
                "cow",
                "elephant",
                "bear",
                "zebra",
                "giraffe",
                "backpack",
                "umbrella",
                "handbag",
                "tie",
                "suitcase",
                "frisbee",
                "skis",
                "snowboard",
                "sports ball",
                "kite",
                "baseball bat",
                "baseball glove",
                "skateboard",
                "surfboard",
                "tennis racket",
                "bottle",
                "wine glass",
                "cup",
                "fork",
                "knife",
                "spoon",
                "bowl",
                "banana",
                "apple",
                "sandwich",
                "orange",
                "broccoli",
                "carrot",
                "hot dog",
                "pizza",
                "donut",
                "cake",
                "chair",
                "couch",
                "potted plant",
                "bed",
                "dining table",
                "toilet",
                "tv",
                "laptop",
                "mouse",
                "remote",
                "keyboard",
                "cell phone",
                "microwave",
                "oven",
                "toaster",
                "sink",
                "refrigerator",
                "book",
                "clock",
                "vase",
                "scissors",
                "teddy bear",
                "hair drier",
                "toothbrush"
            };
    }
}
